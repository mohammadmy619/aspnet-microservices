using Basket.Api.Entities;
using Basket.Api.GrpcServices;
using Basket.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using EventBus.Messages.Events;
using MassTransit;

namespace Basket.Api.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BasketController : ControllerBase
    {
        #region constructor

        private readonly IBasketRepository _basketRepository;
        private readonly DiscountGrpcService _discountService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IMapper _mapper;

        public BasketController(IBasketRepository basketRepository, DiscountGrpcService discountService, IMapper mapper, IPublishEndpoint publishEndpoint)
        {
            _basketRepository = basketRepository;
            _discountService = discountService;
            _mapper = mapper;
            _publishEndpoint = publishEndpoint;
        }

        #endregion

        #region get basket

        [HttpGet("{userName}", Name = "GetBasket")]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> GetBasket(string userName)
        {
            var basket = await _basketRepository.GetUserBasket(userName);
            return Ok(basket ?? new ShoppingCart(userName));
        }

        #endregion

        #region update basket

        [HttpPost]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
        {
            // todo: get data from discount.grpc and calculate final price of product

            foreach (var item in basket.Items)
            {
                var copun = await _discountService.GetDiscount(item.ProductName);
                item.Price -= copun.Amount;
            }

            return Ok(await _basketRepository.UpdateBasket(basket));
        }

        #endregion

        #region remove basket

        [HttpDelete("{userName}", Name = "DeleteBasket")]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteBasket(string userName)
        {
            await _basketRepository.DeleteBasket(userName);
            return Ok();
        }

        #endregion

        #region checkout

        [HttpPost("[action]")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Checkout([FromBody] BasketCheckout basketCheckout)
        {
            // get existing basket with total price
            var basket = await _basketRepository.GetUserBasket(basketCheckout.UserName);
            if (basket == null)
            {
                return BadRequest();
            }


            // create BasketCheckoutEvent -- set total price on basketCheckout event message
            var eventMessage = _mapper.Map<BasketCheckoutEvent>(basketCheckout);
            eventMessage.TotalPrice = basket.TotalPrice;

            // send checkout event to rabbitmq
            await _publishEndpoint.Publish(eventMessage);

            // remove basket
            await _basketRepository.DeleteBasket(basketCheckout.UserName);


            return Accepted();
        }

        #endregion
    }
}
